from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.library_file_list import LibraryFileList
from ...models.problem_details import ProblemDetails
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    kind: str,
    project: str | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["kind"] = kind

    params["project"] = project


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/library/files",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> LibraryFileList | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = LibraryFileList.from_dict(response.json())



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


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[LibraryFileList | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    kind: str,
    project: str | Unset = UNSET,

) -> Response[LibraryFileList | ProblemDetails]:
    """ The pictures, meshes or folders the library's projects hold, a project's own first: what a client
    offers to pick from, where a dialog of its own machine cannot see this one's disk. Folders are those
    a client can write into: every project's and the ones in it, then the exports folder's.

    Args:
        kind (str):
        project (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[LibraryFileList | ProblemDetails]
     """


    kwargs = _get_kwargs(
        kind=kind,
project=project,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    kind: str,
    project: str | Unset = UNSET,

) -> LibraryFileList | ProblemDetails | None:
    """ The pictures, meshes or folders the library's projects hold, a project's own first: what a client
    offers to pick from, where a dialog of its own machine cannot see this one's disk. Folders are those
    a client can write into: every project's and the ones in it, then the exports folder's.

    Args:
        kind (str):
        project (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        LibraryFileList | ProblemDetails
     """


    return sync_detailed(
        client=client,
kind=kind,
project=project,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    kind: str,
    project: str | Unset = UNSET,

) -> Response[LibraryFileList | ProblemDetails]:
    """ The pictures, meshes or folders the library's projects hold, a project's own first: what a client
    offers to pick from, where a dialog of its own machine cannot see this one's disk. Folders are those
    a client can write into: every project's and the ones in it, then the exports folder's.

    Args:
        kind (str):
        project (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[LibraryFileList | ProblemDetails]
     """


    kwargs = _get_kwargs(
        kind=kind,
project=project,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    kind: str,
    project: str | Unset = UNSET,

) -> LibraryFileList | ProblemDetails | None:
    """ The pictures, meshes or folders the library's projects hold, a project's own first: what a client
    offers to pick from, where a dialog of its own machine cannot see this one's disk. Folders are those
    a client can write into: every project's and the ones in it, then the exports folder's.

    Args:
        kind (str):
        project (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        LibraryFileList | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
kind=kind,
project=project,

    )).parsed
