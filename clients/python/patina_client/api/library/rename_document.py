from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.project_contents import ProjectContents
from ...models.project_document_rename_request import ProjectDocumentRenameRequest
from typing import cast



def _get_kwargs(
    *,
    body: ProjectDocumentRenameRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "patch",
        "url": "/api/v1/library/project/document/name",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | ProjectContents | None:
    if response.status_code == 200:
        response_200 = ProjectContents.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | ProjectContents]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ProjectDocumentRenameRequest,

) -> Response[ProblemDetails | ProjectContents]:
    """ Gives a document a new display name. Its file stays.

    Args:
        body (ProjectDocumentRenameRequest): The body of `PATCH
            /api/v1/library/project/document/name`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectContents]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: ProjectDocumentRenameRequest,

) -> ProblemDetails | ProjectContents | None:
    """ Gives a document a new display name. Its file stays.

    Args:
        body (ProjectDocumentRenameRequest): The body of `PATCH
            /api/v1/library/project/document/name`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectContents
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ProjectDocumentRenameRequest,

) -> Response[ProblemDetails | ProjectContents]:
    """ Gives a document a new display name. Its file stays.

    Args:
        body (ProjectDocumentRenameRequest): The body of `PATCH
            /api/v1/library/project/document/name`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectContents]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: ProjectDocumentRenameRequest,

) -> ProblemDetails | ProjectContents | None:
    """ Gives a document a new display name. Its file stays.

    Args:
        body (ProjectDocumentRenameRequest): The body of `PATCH
            /api/v1/library/project/document/name`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectContents
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
