from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.bake_import_request import BakeImportRequest
from ...models.bake_imported import BakeImported
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    *,
    body: BakeImportRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/library/project/bake/import",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> BakeImported | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = BakeImported.from_dict(response.json())



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


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[BakeImported | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: BakeImportRequest,

) -> Response[BakeImported | ProblemDetails]:
    """ Brings maps baked in another program in as a texturing document's bake: each picture is copied where
    the document's own bake goes, as a PNG, and replaces the map of its name there, and the document,
    open or not, reads them as a bake of its mesh, UV set and material as they stand. A PNG is copied as
    it is; any other picture, and a normal whose green runs down, is written as a 16-bit PNG with its
    green up by the GPU engine first.

    Args:
        body (BakeImportRequest): Maps baked in another program, brought in as a texturing
            document's bake (#144): the body of
            `POST /api/v1/library/project/bake/import`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[BakeImported | ProblemDetails]
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
    body: BakeImportRequest,

) -> BakeImported | ProblemDetails | None:
    """ Brings maps baked in another program in as a texturing document's bake: each picture is copied where
    the document's own bake goes, as a PNG, and replaces the map of its name there, and the document,
    open or not, reads them as a bake of its mesh, UV set and material as they stand. A PNG is copied as
    it is; any other picture, and a normal whose green runs down, is written as a 16-bit PNG with its
    green up by the GPU engine first.

    Args:
        body (BakeImportRequest): Maps baked in another program, brought in as a texturing
            document's bake (#144): the body of
            `POST /api/v1/library/project/bake/import`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        BakeImported | ProblemDetails
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: BakeImportRequest,

) -> Response[BakeImported | ProblemDetails]:
    """ Brings maps baked in another program in as a texturing document's bake: each picture is copied where
    the document's own bake goes, as a PNG, and replaces the map of its name there, and the document,
    open or not, reads them as a bake of its mesh, UV set and material as they stand. A PNG is copied as
    it is; any other picture, and a normal whose green runs down, is written as a 16-bit PNG with its
    green up by the GPU engine first.

    Args:
        body (BakeImportRequest): Maps baked in another program, brought in as a texturing
            document's bake (#144): the body of
            `POST /api/v1/library/project/bake/import`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[BakeImported | ProblemDetails]
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
    body: BakeImportRequest,

) -> BakeImported | ProblemDetails | None:
    """ Brings maps baked in another program in as a texturing document's bake: each picture is copied where
    the document's own bake goes, as a PNG, and replaces the map of its name there, and the document,
    open or not, reads them as a bake of its mesh, UV set and material as they stand. A PNG is copied as
    it is; any other picture, and a normal whose green runs down, is written as a 16-bit PNG with its
    green up by the GPU engine first.

    Args:
        body (BakeImportRequest): Maps baked in another program, brought in as a texturing
            document's bake (#144): the body of
            `POST /api/v1/library/project/bake/import`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        BakeImported | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
